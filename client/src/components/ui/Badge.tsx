import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'text-[9px] px-1.5 py-0.5 rounded shrink-0',
  {
    variants: {
      variant: {
        player: 'border border-blue-600 text-blue-400',
        mob:    'bg-red-900 text-red-300',
        npc:    'border border-gray-600 text-gray-400',
        party:  'bg-indigo-800 text-indigo-200',
      },
    },
    defaultVariants: {
      variant: 'npc',
    },
  }
)

interface BadgeProps extends VariantProps<typeof badgeVariants> {
  children: React.ReactNode
  className?: string
}

export function Badge({ variant, children, className }: BadgeProps) {
  return (
    <span className={cn(badgeVariants({ variant }), className)}>
      {children}
    </span>
  )
}
